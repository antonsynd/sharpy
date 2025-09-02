use dot::{Edges, GraphWalk, Id, Labeller, Nodes};
use sharpy_compiler_toolchain::ast::node::{ConstantValue, Node};
use std::collections::HashMap;

pub struct ASTRenderer {
    filter_nodes: Option<Vec<String>>,
}

impl ASTRenderer {
    #[must_use]
    pub const fn new(filter_nodes: Option<Vec<String>>, _debug: bool) -> Self {
        Self { filter_nodes }
    }

    /// Renders the AST as a DOT format string
    /// 
    /// # Errors
    /// Returns an error if DOT rendering fails or if the output contains invalid UTF-8
    pub fn render_ast(&self, ast: &Node) -> anyhow::Result<String> {
        let graph = ASTGraph::new(ast, self.filter_nodes.as_ref());
        let mut output = Vec::new();
        dot::render(&graph, &mut output)?;
        Ok(String::from_utf8(output)?)
    }
}

#[derive(Clone)]
struct NodeInfo {
    label: String,
    node_type: String,
}

struct ASTGraph {
    nodes: Vec<NodeInfo>,
    edges: Vec<(usize, usize)>,
}

impl ASTGraph {
    fn new(root: &Node, filter_nodes: Option<&Vec<String>>) -> Self {
        let mut graph = Self {
            nodes: Vec::new(),
            edges: Vec::new(),
        };

        let mut visited = HashMap::new();
        graph.traverse_node(root, filter_nodes, &mut visited);
        graph
    }

    fn traverse_node(
        &mut self,
        node: &Node,
        filter_nodes: Option<&Vec<String>>,
        visited: &mut HashMap<*const Node, usize>,
    ) -> usize {
        let node_ptr = node as *const Node;

        // Check if we already processed this node
        if let Some(&existing_id) = visited.get(&node_ptr) {
            return existing_id;
        }

        let node_id = self.nodes.len();
        let node_type = Self::get_node_type_name(node);

        // Check filter
        if let Some(filters) = filter_nodes
            && !filters.iter().any(|f| node_type.contains(f))
        {
            // Skip this node type, but still process children
            let mut child_ids = Vec::new();
            let child_ptrs = Self::get_children_ptrs(node);

            for child_ptr in child_ptrs {
                // SAFETY: We know the child nodes are valid for the duration of this function
                let child = unsafe { &*child_ptr };
                let child_id = self.traverse_node(child, filter_nodes, visited);
                child_ids.push(child_id);
            }

            // If this node has only one child, return the child's ID to flatten the tree
            if child_ids.len() == 1 {
                return child_ids[0];
            }

            // Otherwise, create a placeholder node
            let placeholder_label = format!("({} children)", child_ids.len());
            let info = NodeInfo {
                label: placeholder_label,
                node_type: "filtered".to_string(),
            };

            self.nodes.push(info);
            visited.insert(node_ptr, node_id);

            for child_id in child_ids {
                self.edges.push((node_id, child_id));
            }

            return node_id;
        }

        let label = Self::get_node_label(node);
        let child_ptrs = Self::get_children_ptrs(node);

        let info = NodeInfo {
            label,
            node_type,
        };

        self.nodes.push(info);
        visited.insert(node_ptr, node_id);

        // Process children after adding this node
        for child_ptr in child_ptrs {
            // SAFETY: We know the child nodes are valid for the duration of this function
            let child = unsafe { &*child_ptr };
            let child_id = self.traverse_node(child, filter_nodes, visited);
            self.edges.push((node_id, child_id));
        }

        node_id
    }

    fn get_node_type_name(node: &Node) -> String {
        match node {
            Node::Module(_) => "Module".to_string(),
            Node::Assign(_) => "Assign".to_string(),
            Node::BinaryOp(_) => "BinaryOp".to_string(),
            Node::UnaryOp(_) => "UnaryOp".to_string(),
            Node::Lambda(_) => "Lambda".to_string(),
            Node::Dict(_) => "Dict".to_string(),
            Node::Set(_) => "Set".to_string(),
            Node::List(_) => "List".to_string(),
            Node::Tuple(_) => "Tuple".to_string(),
            Node::Call(_) => "Call".to_string(),
            Node::Constant(_) => "Constant".to_string(),
            Node::Attribute(_) => "Attribute".to_string(),
            Node::Subscript(_) => "Subscript".to_string(),
            Node::Name(_) => "Name".to_string(),
            Node::TypedName(_) => "TypedName".to_string(),
            Node::Compare(_) => "Compare".to_string(),
            Node::BoolOp(_) => "BoolOp".to_string(),
            Node::If(_) => "If".to_string(),
            Node::While(_) => "While".to_string(),
            Node::For(_) => "For".to_string(),
            Node::FunctionDef(_) => "FunctionDef".to_string(),
            Node::ClassDef(_) => "ClassDef".to_string(),
            Node::Return(_) => "Return".to_string(),
            Node::Pass(_) => "Pass".to_string(),
            _ => "Unknown".to_string(),
        }
    }

    fn get_node_label(node: &Node) -> String {
        match node {
            Node::Module(m) => format!("Module\\n({} statements)", m.body.len()),
            Node::Assign(_) => "Assignment".to_string(),
            Node::BinaryOp(op) => format!("BinaryOp\\n{:?}", op.op),
            Node::UnaryOp(op) => format!("UnaryOp\\n{:?}", op.op),
            Node::Lambda(l) => {
                let args_count = l.args.args.len();
                let return_type = if l.return_type.is_some() { " -> T" } else { "" };
                format!("Lambda\\n({args_count} args{return_type})")
            }
            Node::Dict(d) => format!("Dict\\n({} items)", d.keys.len()),
            Node::Set(s) => format!("Set\\n({} items)", s.elements.len()),
            Node::List(l) => format!("List\\n({} items)", l.elements.len()),
            Node::Tuple(t) => format!("Tuple\\n({} items)", t.elements.len()),
            Node::Call(c) => format!(
                "Call\\n({} args)",
                c.positional_args.len() + c.keyword_args.len()
            ),
            Node::Constant(c) => format!("Constant\\n{}", Self::format_constant(&c.value)),
            Node::Attribute(a) => format!("Attribute\\n.{}", a.attr),
            Node::Subscript(_) => "Subscript\\n[]".to_string(),
            Node::Name(n) => format!("Name\\n{}", n.id),
            Node::TypedName(tn) => format!("TypedName\\n{}: T", tn.id),
            Node::Compare(c) => format!("Compare\\n{:?}", c.ops),
            Node::BoolOp(b) => format!("BoolOp\\n{:?}", b.op),
            Node::If(_) => "If".to_string(),
            Node::While(_) => "While".to_string(),
            Node::For(_) => "For".to_string(),
            Node::FunctionDef(f) => format!("FunctionDef\\n{}", f.name),
            Node::ClassDef(c) => format!("ClassDef\\n{}", c.name),
            Node::Return(_) => "Return".to_string(),
            Node::Pass(_) => "Pass".to_string(),
            _ => Self::get_node_type_name(node),
        }
    }

    fn format_constant(value: &ConstantValue) -> String {
        match value {
            ConstantValue::None => "None".to_string(),
            ConstantValue::Bool(b) => b.to_string(),
            ConstantValue::Int(i) => i.to_string(),
            ConstantValue::Float(f) => f.to_string(),
            ConstantValue::Complex { real, imag } => format!("{real}+{imag}j"),
            ConstantValue::Str(s) => {
                if s.len() > 20 {
                    format!("\"{}...\"", &s[..17])
                } else {
                    format!("\"{s}\"")
                }
            }
            ConstantValue::Bytes(b) => {
                if b.len() > 10 {
                    format!("b\"{}...\"", String::from_utf8_lossy(&b[..7]))
                } else {
                    format!("b\"{}\"", String::from_utf8_lossy(b))
                }
            }
            ConstantValue::Ellipsis => "...".to_string(),
        }
    }

    fn get_children_ptrs(node: &Node) -> Vec<*const Node> {
        let mut children = Vec::new();

        match node {
            Node::Module(m) => {
                for stmt in &m.body {
                    children.push(std::ptr::from_ref::<Node>(stmt));
                }
            }
            Node::Assign(a) => {
                children.push(&raw const *a.target);
                children.push(&raw const *a.value);
            }
            Node::BinaryOp(op) => {
                children.push(&raw const *op.left);
                children.push(&raw const *op.right);
            }
            Node::UnaryOp(op) => {
                children.push(&raw const *op.operand);
            }
            Node::Lambda(l) => {
                if let Some(ref return_type) = l.return_type {
                    children.push(&raw const **return_type);
                }
                children.push(&raw const *l.body);
            }
            Node::Dict(d) => {
                for (key, value) in d.keys.iter().zip(d.values.iter()) {
                    if let Some(k) = key {
                        children.push(std::ptr::from_ref::<Node>(k));
                    }
                    children.push(std::ptr::from_ref::<Node>(value));
                }
            }
            Node::Set(s) => {
                for element in &s.elements {
                    children.push(std::ptr::from_ref::<Node>(element));
                }
            }
            Node::List(l) => {
                for element in &l.elements {
                    children.push(std::ptr::from_ref::<Node>(element));
                }
            }
            Node::Tuple(t) => {
                for element in &t.elements {
                    children.push(std::ptr::from_ref::<Node>(element));
                }
            }
            Node::Call(c) => {
                children.push(&raw const *c.function);
                for arg in &c.positional_args {
                    children.push(std::ptr::from_ref::<Node>(arg));
                }
                for arg in &c.keyword_args {
                    children.push(std::ptr::from_ref::<Node>(arg));
                }
            }
            Node::Attribute(a) => {
                children.push(&raw const *a.value);
            }
            Node::Subscript(s) => {
                children.push(&raw const *s.value);
                children.push(&raw const *s.slice);
            }
            Node::Compare(c) => {
                children.push(&raw const *c.left);
                for comparator in &c.comparators {
                    children.push(std::ptr::from_ref::<Node>(comparator));
                }
            }
            Node::BoolOp(b) => {
                for value in &b.values {
                    children.push(std::ptr::from_ref::<Node>(value));
                }
            }
            Node::Return(r) => {
                if let Some(ref value) = r.value {
                    children.push(&raw const **value);
                }
            }
            Node::TypedName(tn) => {
                children.push(&raw const *tn.type_);
            }
            // Leaf nodes have no children
            Node::Constant(_) | Node::Name(_) | Node::Pass(_) => {}

            // For unhandled node types, log a warning but don't crash
            _ => {
                eprintln!(
                    "Warning: Unhandled node type in get_children_ptrs: {:?}",
                    Self::get_node_type_name(node)
                );
            }
        }

        children
    }
}

impl<'a> Labeller<'a, usize, (usize, usize)> for ASTGraph {
    fn graph_id(&'a self) -> Id<'a> {
        Id::new("sharpy_ast").unwrap()
    }

    fn node_id(&'a self, n: &usize) -> Id<'a> {
        Id::new(format!("node_{n}")).unwrap()
    }

    fn node_label(&'a self, n: &usize) -> dot::LabelText<'a> {
        let info = &self.nodes[*n];
        dot::LabelText::LabelStr(info.label.clone().into())
    }

    fn node_style(&'a self, n: &usize) -> dot::Style {
        let info = &self.nodes[*n];
        match info.node_type.as_str() {
            "Lambda" => dot::Style::Filled,
            "BinaryOp" | "UnaryOp" | "Compare" | "BoolOp" => dot::Style::Rounded,
            "Constant" | "Name" => dot::Style::Bold,
            "filtered" => dot::Style::Dotted,
            _ => dot::Style::None,
        }
    }

    fn node_color(&'a self, n: &usize) -> Option<dot::LabelText<'a>> {
        let info = &self.nodes[*n];
        let color = match info.node_type.as_str() {
            "Lambda" => "lightblue",
            "BinaryOp" | "UnaryOp" | "Compare" | "BoolOp" => "lightgreen",
            "Constant" => "lightyellow",
            "Name" | "TypedName" => "lightpink",
            "Dict" | "Set" | "List" | "Tuple" => "lightcyan",
            "Call" | "Attribute" | "Subscript" => "lightgray",
            _ => "white",
        };
        Some(dot::LabelText::LabelStr(color.into()))
    }

    fn node_shape(&'a self, n: &usize) -> Option<dot::LabelText<'a>> {
        let info = &self.nodes[*n];
        let shape = match info.node_type.as_str() {
            "Lambda" => "ellipse",
            "BinaryOp" | "UnaryOp" | "Compare" | "BoolOp" => "diamond",
            "Call" => "hexagon",
            "filtered" => "plaintext",
            _ => "box",
        };
        Some(dot::LabelText::LabelStr(shape.into()))
    }
}

impl<'a> GraphWalk<'a, usize, (usize, usize)> for ASTGraph {
    fn nodes(&'a self) -> Nodes<'a, usize> {
        (0..self.nodes.len()).collect()
    }

    fn edges(&'a self) -> Edges<'a, (usize, usize)> {
        self.edges.iter().copied().collect()
    }

    fn source(&'a self, edge: &(usize, usize)) -> usize {
        edge.0
    }

    fn target(&'a self, edge: &(usize, usize)) -> usize {
        edge.1
    }
}
