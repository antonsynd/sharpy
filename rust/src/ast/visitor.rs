use crate::ast::node::Node;

/// Visitor trait for traversing AST nodes.
pub trait Visitor {
    fn visit(&mut self, node: &Node) {
        self.walk(node);
    }

    fn visit_mut(&mut self, node: &mut Node) {
        self.walk_mut(node);
    }

    /// Default traversal method that visits all child nodes
    fn walk(&mut self, node: &Node) {
        match node {
            Node::Assign(n) => self.visit_assign(n),
            Node::BinOp(n) => self.visit_binop(n),
            Node::Call(n) => self.visit_call(n),
            Node::ClassDef(n) => self.visit_class_def(n),
            Node::Constant(n) => self.visit_constant(n),
            Node::FunctionDef(n) => self.visit_function_def(n),
            Node::If(n) => self.visit_if(n),
            Node::Module(n) => self.visit_module(n),
            Node::Name(n) => self.visit_name(n),
            // Add more as needed...
            _ => self.generic_visit(node),
        }
    }

    /// Default mutable traversal method
    fn walk_mut(&mut self, node: &mut Node) {
        match node {
            Node::Assign(n) => self.visit_assign_mut(n),
            Node::BinOp(n) => self.visit_binop_mut(n),
            Node::Call(n) => self.visit_call_mut(n),
            Node::ClassDef(n) => self.visit_class_def_mut(n),
            Node::Constant(n) => self.visit_constant_mut(n),
            Node::FunctionDef(n) => self.visit_function_def_mut(n),
            Node::If(n) => self.visit_if_mut(n),
            Node::Module(n) => self.visit_module_mut(n),
            Node::Name(n) => self.visit_name_mut(n),
            // Add more as needed...
            _ => self.generic_visit_mut(node),
        }
    }

    // Default implementations for specific node types
    fn visit_module(&mut self, node: &crate::ast::node::Module) {
        for stmt in &node.body {
            self.visit(stmt);
        }
    }

    fn visit_module_mut(&mut self, node: &mut crate::ast::node::Module) {
        for stmt in &mut node.body {
            self.visit_mut(stmt);
        }
    }

    fn visit_assign(&mut self, node: &crate::ast::node::Assign) {
        for target in &node.targets {
            self.visit(target);
        }
        self.visit(&node.value);
    }

    fn visit_assign_mut(&mut self, node: &mut crate::ast::node::Assign) {
        for target in &mut node.targets {
            self.visit_mut(target);
        }
        self.visit_mut(&mut node.value);
    }

    fn visit_constant(&mut self, _node: &crate::ast::node::Constant) {}

    fn visit_constant_mut(&mut self, _node: &mut crate::ast::node::Constant) {}

    fn visit_name(&mut self, _node: &crate::ast::node::Name) {}

    fn visit_name_mut(&mut self, _node: &mut crate::ast::node::Name) {}

    fn visit_binop(&mut self, node: &crate::ast::node::BinOp) {
        self.visit(&node.left);
        self.visit(&node.right);
    }

    fn visit_binop_mut(&mut self, node: &mut crate::ast::node::BinOp) {
        self.visit_mut(&mut node.left);
        self.visit_mut(&mut node.right);
    }

    fn visit_call(&mut self, node: &crate::ast::node::Call) {
        self.visit(&node.func);
        for arg in &node.args {
            self.visit(arg);
        }
        for keyword in &node.keywords {
            self.visit(&keyword.value);
        }
    }

    fn visit_call_mut(&mut self, node: &mut crate::ast::node::Call) {
        self.visit_mut(&mut node.func);
        for arg in &mut node.args {
            self.visit_mut(arg);
        }
        for keyword in &mut node.keywords {
            self.visit_mut(&mut keyword.value);
        }
    }

    fn visit_if(&mut self, node: &crate::ast::node::If) {
        self.visit(&node.test);
        for stmt in &node.body {
            self.visit(stmt);
        }
        for stmt in &node.orelse {
            self.visit(stmt);
        }
    }

    fn visit_if_mut(&mut self, node: &mut crate::ast::node::If) {
        self.visit_mut(&mut node.test);
        for stmt in &mut node.body {
            self.visit_mut(stmt);
        }
        for stmt in &mut node.orelse {
            self.visit_mut(stmt);
        }
    }

    fn visit_function_def(&mut self, node: &crate::ast::node::FunctionDef) {
        for decorator in &node.decorator_list {
            self.visit(decorator);
        }
        for arg in &node.args.args {
            if let Some(annotation) = &arg.annotation {
                self.visit(annotation);
            }
        }
        if let Some(returns) = &node.returns {
            self.visit(returns);
        }
        for stmt in &node.body {
            self.visit(stmt);
        }
    }

    fn visit_function_def_mut(&mut self, node: &mut crate::ast::node::FunctionDef) {
        for decorator in &mut node.decorator_list {
            self.visit_mut(decorator);
        }
        for arg in &mut node.args.args {
            if let Some(annotation) = &mut arg.annotation {
                self.visit_mut(annotation);
            }
        }
        if let Some(returns) = &mut node.returns {
            self.visit_mut(returns);
        }
        for stmt in &mut node.body {
            self.visit_mut(stmt);
        }
    }

    fn visit_class_def(&mut self, node: &crate::ast::node::ClassDef) {
        if let Some(base) = &node.base {
            self.visit(base);
        }
    }

    fn visit_class_def_mut(&mut self, node: &mut crate::ast::node::ClassDef) {
        if let Some(base) = &mut node.base {
            self.visit_mut(base);
        }
    }

    /// Fallback for unhandled node types
    fn generic_visit(&mut self, _node: &Node) {}

    fn generic_visit_mut(&mut self, _node: &mut Node) {}
}

/// Helper function to walk all nodes in an AST
pub fn walk<F>(node: &Node, f: F)
where
    F: FnMut(&Node),
{
    struct Walker<F> {
        f: F,
    }

    impl<F> Visitor for Walker<F>
    where
        F: FnMut(&Node),
    {
        fn visit(&mut self, node: &Node) {
            (self.f)(node);
            self.walk(node);
        }
    }

    let mut walker = Walker { f };
    walker.visit(node);
}

/// Helper function to walk all nodes in an AST mutably
pub fn walk_mut<F>(node: &mut Node, f: F)
where
    F: FnMut(&mut Node),
{
    struct WalkerMut<F> {
        f: F,
    }

    impl<F> Visitor for WalkerMut<F>
    where
        F: FnMut(&mut Node),
    {
        fn visit_mut(&mut self, node: &mut Node) {
            (self.f)(node);
            self.walk_mut(node);
        }
    }

    let mut walker = WalkerMut { f };
    walker.visit_mut(node);
}
